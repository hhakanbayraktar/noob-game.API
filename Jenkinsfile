pipeline {

  environment {
    dockerimagename = "hhakanbayraktar/noob-game.api"
    dockerImage = ""
  }

  agent any

  stages {

    
        stage('Install Docker') {
            steps {
                sh 'sudo apt-get update'
                sh 'sudo apt-get install docker-ce docker-ce-cli containerd.io'
            }
        }

    stage('Checkout Source') {
      steps {
        git 'https://github.com/hhakanbayraktar/noob-game.API.git'
      }
    }

    stage('Build image') {
      steps{
        script {
          dockerImage = docker.build dockerimagename
        }
      }
    }

    stage('Pushing Image') {
      environment {
               registryCredential = 'dockerhub-credentials'
           }
      steps{
        script {
          docker.withRegistry( 'https://registry.hub.docker.com', registryCredential ) {
            dockerImage.push("latest")
          }
        }
      }
    }

    stage('Deploying .NET API container to Kubernetes') {
      steps {
        script {
          kubernetesDeploy(configs: "k8s/deploy.yml", "k8s/service.yml")
        }
      }
    }

    stage('Deploying mongoDB container to Kubernetes') {
      steps {
        script {
          kubernetesDeploy(configs: "mongo.yml")
        }
      }
    }
  }
}