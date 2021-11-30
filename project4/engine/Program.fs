// Learn more about F# at http://docs.microsoft.com/dotnet/fsharp

#if INTERACTIVE
#r "nuget: Akka.FSharp"
#endif

open System
open System.Text.RegularExpressions
open Akka.FSharp
open System.Threading

type userManagementMessages =
    | Register of (string * string)
    | Login of (string * string)
    | Logout of (string)
    | Follow of (string * string)
    | Subscribe of (string * string)
    | ValidateUserForTweet of (string * string)
    | ValidateUserForRetweet of (string * int)
    | ValidateUserForMentions of (string * int)
    | UsersToUpdateNewsFeedByTweet of (string * int)
    | UsersToUpdateNewsFeedByRetweet of (string * int)
    | UsersToUpdateNewsFeedByTweetWithHashtag of (string * int)
    | ValidateUserForQueryTweetsSubscribedTo of (string)
    | ValidateUserForQueryTweetsWithMentions of (string)

type newsFeedActor =
    | CreateFeed of (string)
    | UpdateNewsFeedByTweet of (string * int)
    | UpdateNewsFeedByTweetForward of (Set<string> * int)
    | UpdateNewsFeedByRetweet of (string * int)
    | UpdateNewsFeedByRetweetForward of (Set<string> * int)
    | UpdateNewsFeedByTweetWithHashtag of (Set<string> * int)
    | QueryTweetsSubscribedTo of (string)
    | QueryTweetsSubscribedToForward of (string * bool)

type mentionsActor =
    | UpdateMentionsFeed of (string * int)
    | QuerytweetsWithMentions of (string)
    | QuerytweetsWithMentionsForward of (string * bool)

type tweetParserMessages =
    | Parse of (int * string)
    | ParseForwardForUser of (string * bool * int)
    | ParseForwardForHashtag of (Set<string> * int)
    | QueryTweetsWithHashtag of (string)

type tweetManagementMessages =
    | Tweet of (string * string)
    | TweetForward of (string * string * bool * string)
    | Retweet of (string * int)
    | RetweetForward of (string * int * bool * string)

// User management actor
let userManagementActor (mailbox: Actor<_>) =

    // Data to store
    let mutable users = Map.empty
    let mutable followers = Map.empty
    let mutable (subscribers:Map<string, Set<string>>) = Map.empty
    let mutable activeUsers = Set.empty

    let rec loop () = actor {
        let! message = mailbox.Receive ()
        let sender = mailbox.Sender ()

        // Handle message here
        match message with
        | Register(username, password) ->
            users <- Map.add username password users
            followers <- Map.add username Set.empty followers
            printfn "[Register] User %s registered successfully." username

        | Login(username, password) ->
            if(users.ContainsKey username) then
                if(password.Equals(users[username])) then
                    activeUsers <- Set.add username activeUsers
                    printfn "[Login] User %s logged in successfully" username
                else
                    printfn "[Login] User %s entered wrong password. Please try again." username
            else
                printfn "[Login] User %s is not registered." username

        | Logout(username) ->
            if(users.ContainsKey username) then
                if(Set.contains username activeUsers) then
                    activeUsers <- Set.remove username activeUsers
                    printfn "[Logout] User %s logged out successfully." username
                else
                    printfn "[Logout] User %s is not logged in." username
            else
                printfn "[Logout] User %s is not registered." username

        | Follow(follower, following) ->
            if(not(users.ContainsKey follower)) then
                printfn "[Follow] Follower user %s is not registered." follower
            elif(not(followers.ContainsKey following)) then
                printfn "[Follow] Following user %s is not registered." following
            elif(not(activeUsers.Contains follower)) then
                printfn "[Follow] Follower user %s is not logged in" follower
            elif(follower = following) then
                printfn "[Follow] Users cannot follow themselves"
            elif(followers.[following].Contains follower) then
                printfn "[Follow] User %s already follows user %s" follower following
            else
                let mutable followingFollowers = followers.[following]
                followingFollowers <- Set.add follower followingFollowers
                followers <- Map.remove following followers
                followers <- Map.add following followingFollowers followers
                printfn "[Follow] User %s started following user %s" follower following

        | Subscribe(follower, hashtag) ->
            if(not(users.ContainsKey follower)) then
                printfn "[Follow] Follower %s is not registered." follower
            elif(not(subscribers.ContainsKey hashtag)) then
                printfn "[Follow] Subscribing hashtag %s is not registered." hashtag
            elif(not(activeUsers.Contains follower)) then
                printfn "[Follow] Follower user %s is not logged in" follower
            elif(subscribers.[hashtag].Contains follower) then
                printfn "[Follow] User %s already follows hashtag %s" follower hashtag
            else
                let mutable hashtagSubscribers = subscribers.[hashtag]
                hashtagSubscribers <- Set.add follower hashtagSubscribers
                subscribers <- Map.remove hashtag subscribers
                subscribers <- Map.add hashtag hashtagSubscribers subscribers
                printfn "[Follow] User %s started following %s" follower hashtag

        | ValidateUserForTweet(username, tweet) ->
            if(not(users.ContainsKey username)) then
                let status = false
                let errorMessage = "[Tweet] User " + username + " is not registered"
                sender <! TweetForward(username, tweet, status, errorMessage)
            elif(not(activeUsers.Contains username)) then
                let status = false
                let errorMessage = "[Tweet] User " + username + " is not logged in"
                sender <! TweetForward(username, tweet, status, errorMessage)
            else
                let status = true
                let errorMessage = ""
                sender <! TweetForward(username, tweet, status, errorMessage)

        | ValidateUserForRetweet(username, tweetId) ->
            if(not(users.ContainsKey username)) then
                let status = false
                let errorMessage = "[Retweet] User " + username + " is not registered"
                sender <! RetweetForward(username, tweetId, status, errorMessage)
            elif(not(activeUsers.Contains username)) then
                let status = false
                let errorMessage = "[Retweet] User " + username + " is not logged in"
                sender <! RetweetForward(username, tweetId, status, errorMessage)
            else
                let status = true
                let errorMessage = ""
                sender <! RetweetForward(username, tweetId, status, errorMessage)

        | ValidateUserForMentions(mentions, tweetId) ->
            let mutable isRegistered = false
            if(users.ContainsKey mentions) then
                isRegistered <- true
            sender <! ParseForwardForUser(mentions, isRegistered, tweetId)

        | UsersToUpdateNewsFeedByTweet(username, tweetId) ->
            let allFollowers = followers.[username]
            sender <! UpdateNewsFeedByTweetForward(allFollowers, tweetId)
        
        | UsersToUpdateNewsFeedByRetweet(username, tweetId) ->
            let allFollowers = followers.[username]
            sender <! UpdateNewsFeedByRetweetForward(allFollowers, tweetId)

        | UsersToUpdateNewsFeedByTweetWithHashtag(hashtag, tweetId) ->
            if(not(subscribers.ContainsKey hashtag )) then
                subscribers <- Map.add hashtag Set.empty subscribers
                printfn "[Tweet] New hashtag from tweet %d in %s registered successfully" tweetId hashtag
            let allSubscribers = subscribers.[hashtag]
            sender <! ParseForwardForHashtag(allSubscribers, tweetId)

        | ValidateUserForQueryTweetsSubscribedTo(username) ->
            let mutable status = false
            if((users.ContainsKey username) && (activeUsers.Contains username)) then
                status <- true
            sender <! QueryTweetsSubscribedToForward(username, status)

        | ValidateUserForQueryTweetsWithMentions(mentions) ->
            let mutable status = false
            if((users.ContainsKey mentions) && (activeUsers.Contains mentions)) then
                status <- true
            sender <! QuerytweetsWithMentionsForward(mentions, status)

        return! loop ()
    }
    loop ()


// News feed
let newsFeedActor userManagementRef (mailbox: Actor<_>) =

    // Data to store
    let mutable newsFeed = Map.empty

    let rec loop () = actor {
        let! message = mailbox.Receive ()
        // Handle message here
        match message with
        | CreateFeed(username) ->
            newsFeed <- Map.add username Set.empty newsFeed
            printfn "News feed for %s created successfully" username

        | UpdateNewsFeedByTweet(username, tweetId) ->
            userManagementRef <! UsersToUpdateNewsFeedByTweet(username, tweetId)

        | UpdateNewsFeedByTweetForward(allFollowers, tweetId) ->
            for follower in allFollowers do
                // Update news feed
                if(newsFeed.ContainsKey follower) then
                    let mutable followerNewsFeed = newsFeed.[follower]
                    followerNewsFeed <- Set.add tweetId followerNewsFeed
                    newsFeed <- Map.remove follower newsFeed
                    newsFeed <- Map.add follower followerNewsFeed newsFeed
                else
                    newsFeed <- Map.add follower (Set.empty.Add(tweetId)) newsFeed
                printfn "[News feed] Tweet %d added to the news feed of follower %s" tweetId follower

        | UpdateNewsFeedByRetweet(username, tweetId) ->
            userManagementRef <! UsersToUpdateNewsFeedByRetweet(username, tweetId)

        | UpdateNewsFeedByRetweetForward(allFollowers, tweetId) ->
            for follower in allFollowers do
                // Update news feed
                if(newsFeed.ContainsKey follower) then
                    let mutable followerNewsFeed = newsFeed.[follower]
                    followerNewsFeed <- Set.add tweetId followerNewsFeed
                    newsFeed <- Map.remove follower newsFeed
                    newsFeed <- Map.add follower followerNewsFeed newsFeed
                else
                    newsFeed <- Map.add follower (Set.empty.Add(tweetId)) newsFeed
                printfn "[News feed] Retweet %d added to the news feed of follower %s" tweetId follower

        | UpdateNewsFeedByTweetWithHashtag(allSubscribers, tweetId) ->
            for subscriber in allSubscribers do
                // Update news feed
                if(newsFeed.ContainsKey subscriber) then
                    let mutable followerNewsFeed = newsFeed.[subscriber]
                    followerNewsFeed <- Set.add tweetId followerNewsFeed
                    newsFeed <- Map.remove subscriber newsFeed
                    newsFeed <- Map.add subscriber followerNewsFeed newsFeed
                else
                    newsFeed <- Map.add subscriber (Set.empty.Add(tweetId)) newsFeed
                printfn "[News feed] Tweet with hashtag %d added to the news feed of follower %s" tweetId subscriber

        | QueryTweetsSubscribedTo(username) ->
            // Validate user
            userManagementRef <! ValidateUserForQueryTweetsSubscribedTo(username)

        | QueryTweetsSubscribedToForward(username, status) ->
            if(status) then
                if(newsFeed.ContainsKey username) then
                    printfn "[News feed] News feed of user %s: %A" username newsFeed.[username]
                else
                    printfn "[News feed] News feed of user %s is empty" username
            else
                printfn "[News feed] User %s is either not registered or not active" username

        return! loop ()
    }
    loop ()


let mentionsFeedActor userManagementRef (mailbox: Actor<_>) =

    // Data to store
    let mutable mentionsFeed = Map.empty

    let rec loop () = actor {
        let! message = mailbox.Receive ()
        // Handle message here
        match message with
        | UpdateMentionsFeed(mentions, tweetId) ->
            // Update mentions feed
            if(mentionsFeed.ContainsKey mentions) then
                let mutable mentionsMentionsFeed = mentionsFeed.[mentions]
                mentionsMentionsFeed <- Set.add tweetId mentionsMentionsFeed
                mentionsFeed <- Map.remove mentions mentionsFeed
                mentionsFeed <- Map.add mentions mentionsMentionsFeed mentionsFeed
            else
                mentionsFeed <- Map.add mentions (Set.empty.Add(tweetId)) mentionsFeed

        | QuerytweetsWithMentions(mentions) ->
            // Validate user
            userManagementRef <! ValidateUserForQueryTweetsWithMentions(mentions)

        | QuerytweetsWithMentionsForward(mentions, status) ->
            if(status) then
                if(mentionsFeed.ContainsKey mentions) then
                    printfn "[Mentions] Mentions of user %s: %A" mentions mentionsFeed.[mentions]
                else
                    printfn "[Mentions] User %s has no mentions" mentions
            else
                printfn "[Mnetions] User %s is either not registered or not active" mentions
        
        return! loop ()
    }
    loop ()


let tweetParserActor userManagementRef newsFeedRef mentionsRef (mailbox: Actor<_>) =

    // Data to store
    let mutable hashtagsTweets:Map<string, Set<int>> = Map.empty

    let rec loop () = actor {
        let! message = mailbox.Receive ()
        
        // Handle message here
        match message with
        | Parse(tweetId, tweet) ->
            // Find hashtags
            let mutable (hashtags:Set<string>) = Set.empty;
            let hashtagRegex = new Regex(@"#\w+")
            let hashtagMatches = hashtagRegex.Matches(tweet)
            for hashtagMatch in hashtagMatches do
                let hashtag = hashtagMatch.Value
                hashtags <- Set.add hashtag hashtags;
                if(hashtagsTweets.ContainsKey hashtag) then
                    let mutable hashtagTweets = hashtagsTweets[hashtag]
                    hashtagTweets <- Set.add tweetId hashtagTweets
                    hashtagsTweets <- Map.remove hashtag hashtagsTweets
                    hashtagsTweets <- Map.add hashtag hashtagTweets hashtagsTweets
                else
                    hashtagsTweets <- Map.add hashtag (Set.empty.Add(tweetId)) hashtagsTweets
                // Validate hashtag
                userManagementRef <! UsersToUpdateNewsFeedByTweetWithHashtag(hashtag, tweetId)

            // Find mentions
            let mentionsRegex = new Regex(@"@\w+")
            let mentionsMatches = mentionsRegex.Matches(tweet)
            for mentionsMatch in mentionsMatches do
                let mentions = mentionsMatch.Value.Substring(1)
                // Validate mentions
                userManagementRef <! ValidateUserForMentions(mentions, tweetId)

        | ParseForwardForUser(mentions, isRegistered, tweetId) ->
            if(isRegistered) then
                mentionsRef <! UpdateMentionsFeed(mentions, tweetId)
                
        | ParseForwardForHashtag(allSubscribers, tweetId) ->
            newsFeedRef <! UpdateNewsFeedByTweetWithHashtag(allSubscribers, tweetId)

        | QueryTweetsWithHashtag(hashtag) ->
            if(hashtagsTweets.ContainsKey hashtag) then
                printfn "[Hashtags] Tweet with hashtag %s: %A" hashtag hashtagsTweets.[hashtag]
            else
                printfn "[Hashtags] No tweets with hashtags %s" hashtag
            
        return! loop ()
    }
    loop ()


// Tweet handler
let tweetManagementActor userManagementRef newsFeedRef tweetParserRef (mailbox: Actor<_>) =

    // Data to store
    let mutable (tweets: List<List<string>>) = []

    let rec loop () = actor {
        let! message = mailbox.Receive ()
        
        // Handle message here
        match message with
        | Tweet(username, tweet) ->
            // Validate user
            userManagementRef <! ValidateUserForTweet(username, tweet)

        | TweetForward(username, tweet, status, errorMessage) ->
            if(status) then
                // Process the tweet further if user is validated
                printfn "[Tweet] User %s validated" username
                // Send tweet to the news feed actor
                printfn "[Tweet] User %s tweeted: %s" username tweet
                let tweetId = tweets.Length
                tweets <- List.append tweets [[username; tweet]]
                newsFeedRef <! UpdateNewsFeedByTweet(username, tweetId)
                // Send tweet to the tweet parser actor
                tweetParserRef <! Parse(tweetId, tweet)
            else
                printfn "[Tweet] Could not validate user %s due to %s" username errorMessage

        | Retweet(username, tweetId) ->
            // Validate user
            userManagementRef <! ValidateUserForRetweet(username, tweetId)

        | RetweetForward(username, tweetId, status, errorMessage) ->
            // Process the tweet further if user is validated
            if(status) then
                printfn "[Retweet] User %s validated" username
                // Process the tweet further if tweet exists
                if(tweetId < tweets.Length) then
                    newsFeedRef <! UpdateNewsFeedByRetweet(username, tweetId)
                else
                    printfn "[Retweet] Tweet does not exist"
            else
                printfn "[Retweet] Could not validate user %s due to %s" username errorMessage

        return! loop ()
    }
    loop ()


[<EntryPoint>]
let main argv =
    // Create system
    let system = System.create "my-system" (Configuration.load())

    // Create all services
    let userManagementRef = spawn system "userManagementActor" userManagementActor
    let newsFeedRef = spawn system "newsFeedActor" (newsFeedActor userManagementRef)
    let mentionsRef = spawn system "mentionsActor" (mentionsFeedActor userManagementRef)
    let tweetParserRef = spawn system "tweetParserActor" (tweetParserActor userManagementRef newsFeedRef mentionsRef)
    let tweetManagementRef = spawn system "tweetManagementActor" (tweetManagementActor userManagementRef newsFeedRef tweetParserRef)

    // User registration
    userManagementRef <! Register("nikhil_saoji", "nikhil_saoji")
    userManagementRef <! Register("gauri_bodke", "gauri_bodke")
    userManagementRef <! Register("prasad_hadkar", "prasad_hadkar")

    // Login
    userManagementRef <! Login("nikhil_saoji", "nikhil_saoji")
    userManagementRef <! Login("gauri_bodke", "gauri_bodke")
    userManagementRef <! Login("prasad_hadkar", "prasad_hadkar")

    // Logout
    // userManagementRef <! Logout("nikhil_saoji")
    // userManagementRef <! Logout("gauri_bodke")
    // userManagementRef <! Logout("prasad_hadkar")

    // Follow
    userManagementRef <! Follow("nikhil_saoji", "gauri_bodke")
    userManagementRef <! Follow("nikhil_saoji", "prasad_hadkar")
    userManagementRef <! Follow("prasad_hadkar", "gauri_bodke")
    userManagementRef <! Follow("gauri_bodke", "prasad_hadkar")

    // Tweet
    tweetManagementRef <! Tweet("gauri_bodke", "My name is Gauri #uf #hello. @nikhil_saoji")
    tweetManagementRef <! Tweet("gauri_bodke", "I study at UF #uf #corona @prasad_hadkar")
    tweetManagementRef <! Tweet("nikhil_saoji", "Hello! #corona @gauri_bodke @prasad_hadkar")
    tweetManagementRef <! Tweet("prasad_hadkar", "I love cricket #bye")

    // Retweet
    tweetManagementRef <! Retweet("gauri_bodke", 2);
    tweetManagementRef <! Retweet("nikhil_saoji", 1);

    // Subscribe
    Thread.Sleep(5000)
    userManagementRef <! Subscribe("gauri_bodke", "#uf");
    tweetManagementRef <! Tweet("nikhil_saoji", "I study at #uf");
    Thread.Sleep(1000)
    newsFeedRef <! QueryTweetsSubscribedTo("nikhil_saoji")
    Thread.Sleep(1000)
    tweetParserRef <! QueryTweetsWithHashtag("#uf")
    Thread.Sleep(1000)
    mentionsRef <! QuerytweetsWithMentions("prasad_hadkar")

    Thread.Sleep(6000)
    0 // return an integer exit code